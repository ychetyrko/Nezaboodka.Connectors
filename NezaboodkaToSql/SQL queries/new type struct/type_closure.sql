CREATE DATABASE IF NOT EXISTS `nz_test_closure`;

USE `nz_test_closure`;

CREATE TABLE `nz_test_closure`.`type`(
	`id` INT PRIMARY KEY NOT NULL AUTO_INCREMENT,
	`name` VARCHAR(128) NOT NULL UNIQUE CHECK(`name` != ''),
	`table_name` VARCHAR(64) NOT NULL UNIQUE CHECK(`table_name` != ''),
	`base_type_name` VARCHAR(128) COLLATE `utf8_general_ci` CHECK(`base_type_name` != '')
) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;

CREATE TABLE `nz_test_closure`.`type_closure`(
	`ancestor` INT NOT NULL,
	`descendant` INT NOT NULL,
	`is_straight` BOOLEAN NOT NULL DEFAULT FALSE,
	
	FOREIGN KEY(`ancestor`)
		REFERENCES `nz_test_closure`.`type`(`id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY(`descendant`)
		REFERENCES `nz_test_closure`.`type`(`id`)
		ON DELETE CASCADE,
	
	CONSTRAINT `uc_keys`
		UNIQUE (`ancestor`, `descendant`)
) ENGINE=`InnoDB`;

CREATE TABLE `nz_test_closure`.`field` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`owner_type_name` VARCHAR(128) NOT NULL CHECK(`owner_type_name` != ''),
	`owner_type_id` INT DEFAULT NULL,
	`name` VARCHAR(128) NOT NULL CHECK(`name` != ''),
	`col_name` VARCHAR(64) NOT NULL COLLATE `utf8_general_ci` CHECK(`col_name` != ''),
	`type_name` VARCHAR(64) NOT NULL CHECK(`type_name` != ''),
	`ref_type_id` INT DEFAULT NULL,
	`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
	`compare_options` ENUM
		(
			'None',
			'IgnoreCase',
			'IgnoreNonSpace',
			'IgnoreSymbols',
			'IgnoreKanaType',
			'IgnoreWidth',
			'OrdinalIgnoreCase',
			'StringSort',
			'Ordinal'
		) NOT NULL DEFAULT 'None',
	`back_ref_name` VARCHAR(128) DEFAULT NULL CHECK(`back_ref_name` != ''),
	`back_ref_id` INT DEFAULT NULL,
	
	FOREIGN KEY(`owner_type_id`)
		REFERENCES `nz_test_closure`.`type`(`id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY(`ref_type_id`)
		REFERENCES `nz_test_closure`.`type`(`id`)
		ON DELETE RESTRICT,
	
	FOREIGN KEY(`back_ref_id`)
		REFERENCES `nz_test_closure`.`field`(`id`)
		ON DELETE SET NULL
) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;

CREATE TABLE `nz_test_closure`.`db_key` (
	`sys_id` BIGINT(0) PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`rev_flags` BIGINT(0) NOT NULL DEFAULT 1,
	`real_type_id` INT NOT NULL,
	
	FOREIGN KEY (`real_type_id`)
		REFERENCES `nz_test_closure`.`type`(`id`)
		ON DELETE CASCADE
) ENGINE=`InnoDB`;

CREATE TABLE `nz_test_closure`.`list` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`owner_id` BIGINT(0) NOT NULL,
	`type_id` INT NOT NULL,
	
	FOREIGN KEY (`owner_id`)
		REFERENCES `nz_test_closure`.`db_key`(`sys_id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY (`type_id`)
		REFERENCES `nz_test_closure`.`type`(`id`)
		ON DELETE CASCADE
) ENGINE=`InnoDB`;

CREATE TABLE `nz_test_closure`.`list_item` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`list_id` INT NOT NULL,
	`ref` BIGINT(0) NOT NULL,
	
	FOREIGN KEY (`list_id`)
		REFERENCES `nz_test_closure`.`list`(`id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY (`ref`)
		REFERENCES `nz_test_closure`.`db_key`(`sys_id`)
		ON DELETE CASCADE
) ENGINE=`InnoDB`;

#--------------------------------------------------

DELIMITER //
DROP PROCEDURE IF EXISTS before_alter_types //
CREATE PROCEDURE before_alter_types()
BEGIN
	DROP TABLE IF EXISTS `nz_test_closure`.`type_add_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`type_add_list`(
		`id` INT PRIMARY KEY NOT NULL UNIQUE AUTO_INCREMENT,
		`name` VARCHAR(128) NOT NULL UNIQUE CHECK(`name` != ''),
		`table_name` VARCHAR(64) NOT NULL UNIQUE CHECK(`table_name` != ''),
		`base_type_name` VARCHAR(128) CHECK(`table_name` != '')
	) ENGINE=`MEMORY` DEFAULT CHARSET=`utf8` COLLATE `utf8_general_ci`;

# ---> type_rem_list
END //

DELIMITER //
DROP PROCEDURE IF EXISTS add_types //
CREATE PROCEDURE add_types()
BEGIN
	DECLARE current_ord INT DEFAULT 0;
	
	DROP TABLE IF EXISTS `nz_test_closure`.`type_add_queue`;
	CREATE TEMPORARY TABLE `nz_test_closure`.`type_add_queue`(
		`ord` INT NOT NULL,
		`id` INT NOT NULL unique,	# to IGNORE already inserted elements
		FOREIGN KEY (`id`)
			REFERENCES `nz_test_closure`.`type_add_list`(`id`)
			ON DELETE CASCADE
	) ENGINE=`MEMORY`;

	# As temporary table can't be referred to multiple times in the same query:
	# https://dev.mysql.com/doc/refman/5.7/en/temporary-table-problems.html
	DROP TABLE IF EXISTS `nz_test_closure`.`type_temp_queue_buf`;
	CREATE TEMPORARY TABLE `nz_test_closure`.`type_temp_queue_buf`(
		`id` INT NOT NULL,
		FOREIGN KEY (`id`)
			REFERENCES `nz_test_closure`.`type_add_list`(`id`)
			ON DELETE CASCADE
	) ENGINE=`MEMORY`;

	# Same reason
	DROP TABLE IF EXISTS `nz_test_closure`.`type_inserted_list_buf`;
	CREATE TEMPORARY TABLE `nz_test_closure`.`type_inserted_list_buf`(
		`id` INT NOT NULL unique,
	`name` VARCHAR(128) NOT NULL,
		FOREIGN KEY (`id`)
			REFERENCES `nz_test_closure`.`type_add_list`(`id`)
			ON DELETE CASCADE
	) ENGINE=`MEMORY`;

	# First: insert types with NO parents (roots)
	SET current_ord = 0;
	INSERT INTO `nz_test_closure`.`type_add_queue`
	(`ord`, `id`)
	SELECT current_ord, tadd.`id`
	FROM `nz_test_closure`.`type_add_list` AS tadd
	WHERE tadd.`base_type_name` is NULL;

	# Second: insert types with EXISTING parents
	SET current_ord = current_ord + 1;
	INSERT INTO `nz_test_closure`.`type_add_queue`
	(`ord`, `id`)
	SELECT current_ord, tadd.`id`
	FROM `nz_test_closure`.`type_add_list` AS tadd
	JOIN `nz_test_closure`.`type` AS t
	ON tadd.`base_type_name` = t.`name`;

	# Third: insert types with parents just inserted (main ordering loop)
	order_loop: LOOP
		DELETE FROM `nz_test_closure`.`type_temp_queue_buf`;
		DELETE FROM `nz_test_closure`.`type_inserted_list_buf`;
		SET current_ord = current_ord + 1;

		INSERT INTO `nz_test_closure`.`type_inserted_list_buf`
		(`id`, `name`)
		SELECT tadd.`id`, tadd.`name`
		FROM `nz_test_closure`.`type_add_list` AS tadd
		JOIN `nz_test_closure`.`type_add_queue` AS q
		ON tadd.`id` = q.`id`;

		INSERT INTO `nz_test_closure`.`type_temp_queue_buf`
		(`id`)
		SELECT tadd.`id`
		FROM `nz_test_closure`.`type_add_list` AS tadd
		JOIN `nz_test_closure`.`type_inserted_list_buf` AS insbuf
		ON tadd.`base_type_name` = insbuf.`name`;

		INSERT IGNORE INTO `nz_test_closure`.`type_add_queue`
		(`ord`, `id`)
		SELECT current_ord, b.`id`
		FROM `nz_test_closure`.`type_temp_queue_buf` AS b;

		IF (ROW_COUNT() = 0) THEN
			LEAVE order_loop;
		END IF;
	END LOOP;

	DELETE FROM `nz_test_closure`.`type_inserted_list_buf`;
	DROP TABLE `nz_test_closure`.`type_inserted_list_buf`;

	DELETE FROM `nz_test_closure`.`type_temp_queue_buf`;
	DROP TABLE `nz_test_closure`.`type_temp_queue_buf`;
/*
# Debug
	SELECT q.`ord`, tadd.`name`, tadd.`base_type_name`
	FROM `nz_test_closure`.`type_add_list` AS tadd
	JOIN `nz_test_closure`.`type_add_queue` AS q
	ON tadd.`id` = q.`id`
	ORDER BY q.`ord`;
*/
# ------------------------------------------------------------------------
	DROP TABLE IF EXISTS `nz_test_closure`.`new_type`;
	CREATE TEMPORARY TABLE `nz_test_closure`.`new_type`(
		`id` INT NOT NULL,
		FOREIGN KEY (`id`)
			REFERENCES `nz_test_closure`.`type`(`id`)
			ON DELETE CASCADE
	) ENGINE=`MEMORY`;
	
	BEGIN
		DECLARE t_name VARCHAR(128) DEFAULT NULL;
		
		DECLARE done boolean DEFAULT FALSE;
		DECLARE cur CURSOR FOR
			SELECT tadd.`name`
			FROM `nz_test_closure`.`type_add_list` AS tadd
			JOIN `nz_test_closure`.`type_add_queue` AS q
			ON tadd.`id` = q.`id`
			ORDER BY q.`ord`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET done = true;
		OPEN cur;

		FETCH cur
		INTO t_name;
		WHILE NOT done DO
			CALL add_type(t_name);
			
			FETCH cur
			INTO t_name;
		END WHILE;
		
		CLOSE cur;
	END;
		
	DELETE FROM `nz_test_closure`.`type_add_queue`;
	DROP TABLE `nz_test_closure`.`type_add_queue`;
	
# Process all new types
	BEGIN
		DECLARE db_name VARCHAR(64) DEFAULT 'nz_test_closure';

		DECLARE t_type_name VARCHAR(128) DEFAULT NULL;
		DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
		DECLARE fields_defs TEXT;
		DECLARE fields_constraints TEXT;

		DECLARE types_done boolean DEFAULT FALSE;
		DECLARE new_type_cur CURSOR FOR
			SELECT t.`name` ,t.`table_name`
			FROM `nz_test_closure`.`type` AS t
			JOIN `nz_test_closure`.`new_type` AS n
			WHERE t.`id` = n.`id`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET types_done = true;
		
		OPEN new_type_cur;

		FETCH new_type_cur	
		INTO t_type_name, t_table_name;
		WHILE NOT types_done DO

			CALL get_type_fields_and_constraints(t_type_name, TRUE, fields_defs, fields_constraints);

			IF (CHAR_LENGTH(fields_defs) > 0) THEN 
				SET fields_defs = CONCAT(',', fields_defs);
			END IF;

			IF (CHAR_LENGTH(fields_constraints) > 0) THEN 
				SET fields_constraints = CONCAT(',', fields_constraints);
			END IF;

			# Create table for type with all ancestors' fields
			#  (table name can't be a parameter => prepare each time)
			SET @prep_str = CONCAT('
				CREATE TABLE `', db_name ,'`.`', t_table_name, '` (
					id BIGINT(0) PRIMARY KEY NOT NULL

					', fields_defs ,',

					FOREIGN KEY (id)
						REFERENCES `', db_name ,'`.`db_key`(`sys_id`)
						ON DELETE CASCADE

						', fields_constraints, '

				) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;
			');
/*
# Debug
			SELECT @prep_str;
*/
			PREPARE p_CREATE_table FROM @prep_str;
			EXECUTE p_CREATE_table;
			DEALLOCATE PREPARE p_CREATE_table;

			FETCH new_type_cur
			INTO t_type_name, t_table_name;
		END WHILE;
		
		CLOSE new_type_cur;
	END;

	DROP TABLE `nz_test_closure`.`new_type`;
	TRUNCATE TABLE `nz_test_closure`.`type_add_list`;
END //

DELIMITER //
DROP PROCEDURE IF EXISTS add_type //
CREATE PROCEDURE add_type(IN type_name VARCHAR(128))
BEGIN
	DECLARE tbl_name VARCHAR(64) DEFAULT NULL;
	DECLARE base_name VARCHAR(128) DEFAULT NULL;
	DECLARE base_id INT DEFAULT NULL;
	DECLARE type_id INT DEFAULT NULL;
	
	SELECT tadd.`table_name`, tadd.`base_type_name`
	INTO tbl_name, base_name
	FROM `nz_test_closure`.`type_add_list` AS tadd
	WHERE tadd.`name` = type_name
	LIMIT 1;
	
	SELECT t.`id`
	INTO base_id
	FROM `nz_test_closure`.`type` AS t
	WHERE t.`name` = base_name
	LIMIT 1;

	INSERT INTO `nz_test_closure`.`type`
	(`name`, `base_type_name`, `table_name`)
	VALUE
	(type_name, base_name, tbl_name);
	
	SELECT LAST_INSERT_ID()
	INTO type_id;

	INSERT INTO `nz_test_closure`.`type_closure`
	(`ancestor`, `descendant`)
	SELECT clos.`ancestor`, type_id
	FROM `nz_test_closure`.`type_closure` AS clos
	WHERE clos.`descendant` = base_id
	UNION
	SELECT type_id, type_id;
	
	INSERT INTO `nz_test_closure`.`new_type` (`id`)
	VALUE (type_id);
	
	DELETE FROM `nz_test_closure`.`type_add_list`
	WHERE `name` = type_name;
END //

#--------------------------------------------------

DELIMITER //
DROP PROCEDURE IF EXISTS get_type_fields_and_constraints //
CREATE PROCEDURE get_type_fields_and_constraints
	(IN c_type_name VARCHAR(128), IN inheriting boolean,
	OUT fields_defs TEXT, OUT fields_constraints TEXT)
BEGIN
	DECLARE c_type_id INT DEFAULT NULL;
	
	DECLARE cf_id INT DEFAULT NULL;	# for constraints names
	DECLARE cf_col_name VARCHAR(64) DEFAULT NULL;
	DECLARE cf_type_name VARCHAR(128) DEFAULT NULL;
	DECLARE cf_ref_type_id INT DEFAULT NULL;
	DECLARE cf_is_list boolean DEFAULT FALSE;
	DECLARE cf_compare_options VARCHAR(64);

	SET fields_defs = '';
	SET fields_constraints = '';

	SELECT `id`
	INTO c_type_id
	FROM `nz_test_closure`.`type` AS t
	WHERE t.`name` = c_type_name;

/*
# Debug
	SELECT concat('Start ', c_type_name, '(', c_type_id,')', ' altering.') AS debug;
*/
	IF inheriting THEN
	BEGIN	# get all parents' fields
		DECLARE fields_done boolean DEFAULT FALSE;
		DECLARE fields_cur CURSOR FOR
			SELECT f.`id`, f.`col_name`, f.`type_name`, f.`ref_type_id`,
				f.`is_list`, f.`compare_options`
			FROM `nz_test_closure`.`field` AS f
			WHERE f.`owner_type_id` IN (
				SELECT clos.`ancestor`
				FROM `nz_test_closure`.`type_closure` AS clos
				WHERE clos.`descendant` = c_type_id
			);
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET fields_done = true;

		OPEN fields_cur;

		FETCH fields_cur
		INTO cf_id, cf_col_name, cf_type_name, cf_ref_type_id,
			cf_is_list, cf_compare_options;
		WHILE NOT fields_done DO

			CALL update_fields_def_constr(fields_defs, fields_constraints, inheriting,
				c_type_id, cf_id, cf_col_name, cf_type_name, cf_ref_type_id,
                cf_is_list, cf_compare_options);
			
			FETCH fields_cur
			INTO cf_id, cf_col_name, cf_type_name, cf_ref_type_id,
				cf_is_list, cf_compare_options;
		END WHILE;
	END;
	ELSE	# NOT inheriting
	BEGIN	# get only NEW fields
		DECLARE fields_done boolean DEFAULT FALSE;
		DECLARE fields_cur CURSOR FOR
			SELECT f.`id`, f.`col_name`, f.`type_name`, f.`ref_type_id`,
				f.`is_list`, f.`compare_options`
			FROM `nz_test_closure`.`new_field` AS newf
			left JOIN `nz_test_closure`.`field` AS f
			ON f.`id` = newf.`id`
			WHERE f.`owner_type_id` IN (
				SELECT clos.`ancestor`
				FROM `nz_test_closure`.`type_closure` AS clos
				WHERE clos.`descendant` = c_type_id
			);
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET fields_done = true;

		OPEN fields_cur;

		FETCH fields_cur
		INTO cf_id, cf_col_name, cf_type_name, cf_ref_type_id,
			cf_is_list, cf_compare_options;
		WHILE NOT fields_done DO

			CALL update_fields_def_constr(fields_defs, fields_constraints, inheriting,
				c_type_id, cf_id, cf_col_name, cf_type_name, cf_ref_type_id,
                cf_is_list, cf_compare_options);
			
			FETCH fields_cur
			INTO cf_id, cf_col_name, cf_type_name, cf_ref_type_id,
				cf_is_list, cf_compare_options;
		END WHILE;
	END;
	END IF;

	IF (LEFT(fields_defs, 1) = ',') THEN
		SET fields_defs = SUBSTRING(fields_defs, 2);
	END IF;

	IF (LEFT(fields_constraints, 1) = ',') THEN
		SET fields_constraints = SUBSTRING(fields_constraints, 2);
	END IF;

	#SELECT fields_defs;
	#SELECT fields_constraints;
	#SELECT concat('END ', c_type_name, '(', c_type_id,')', ' altering.') AS debug;
END //
#--------------------------------------------------

DELIMITER //
DROP PROCEDURE IF EXISTS update_fields_def_constr //
CREATE PROCEDURE update_fields_def_constr
	(INOUT f_defs TEXT, INOUT f_constrs TEXT, IN inheriting boolean,
	IN c_type_id INT, IN cf_id INT, IN cf_col_name VARCHAR(64), IN cf_type_name VARCHAR(128),
	IN cf_ref_type_id INT, IN cf_is_list boolean, IN cf_compare_options VARCHAR(128))
BEGIN
	DECLARE constr_add_prefix TEXT DEFAULT 'CONSTRAINT FK_';
	DECLARE constr_add_prefix_full TEXT DEFAULT '';
	DECLARE field_type VARCHAR(128);
	DECLARE nullable_sign_pos INT DEFAULT 0;

	IF NOT inheriting THEN
		SET constr_add_prefix = CONCAT('ADD ', constr_add_prefix);
	END IF;

	IF cf_ref_type_id IS NULL THEN
		IF NOT cf_is_list THEN
			SET field_type = cf_type_name;
			
			IF field_type LIKE 'VARCHAR(%' OR field_type = 'TEXT' THEN
				IF cf_compare_options = 'IgnoreCase' THEN
					SET field_type = CONCAT(field_type, ' COLLATE `utf8_general_ci`');
				END IF;
			ELSE	# not string
				IF (RIGHT(field_type, 1) = '?') THEN
					SET field_type =
						SUBSTRING(field_type FROM 1 FOR CHAR_LENGTH(field_type)-1);
				ELSE
					SET field_type = CONCAT(field_type, ' NOT NULL');
				END IF;
			END IF;
			
		ELSE	# list
			SET field_type = 'BLOB';
		END IF;
	ELSE	# reference

		SET constr_add_prefix_full = CONCAT('
			',constr_add_prefix, c_type_id, '_', cf_id);
		
		IF NOT cf_is_list THEN
			SET field_type = 'BIGINT(0)';
			SET f_constrs = CONCAT(f_constrs, ',
				', constr_add_prefix_full,'
				FOREIGN KEY (`', cf_col_name,'`)
					REFERENCES `db_key`(`sys_id`)
					ON DELETE SET NULL
					ON UPDATE SET NULL');
		ELSE	# list
			SET field_type = 'INT';
			SET f_constrs = CONCAT(f_constrs, ',
				', constr_add_prefix_full,'
				FOREIGN KEY (`', cf_col_name,'`)
					REFERENCES `list`(`id`)
					ON DELETE SET NULL');
		END IF;
	END IF;

	SET f_defs = CONCAT(f_defs, ', `', cf_col_name, '` ', field_type);
END //
#--------------------------------------------------

DELIMITER //
DROP PROCEDURE IF EXISTS remove_type //
CREATE PROCEDURE remove_type(IN type_name VARCHAR(128))
BEGIN
	DECLARE desc_count INT DEFAULT NULL;
	DECLARE type_id INT DEFAULT NULL;

	SELECT `id`
	INTO type_id
	FROM `nz_test_closure`.`type` AS t
	WHERE t.`name` = type_name
	LIMIT 1;

	# Сheck if terminating type
	SELECT COUNT(clos.`ancestor`)
	INTO desc_count
	FROM `nz_test_closure`.`type_closure` AS clos
	WHERE clos.`ancestor` = type_id;
	
	IF (desc_count = 1) THEN
		DELETE FROM `nz_test_closure`.`type`
		WHERE `id` = type_id;

# ---> DROP TABLE

		ELSE
			SIGNAL SQLSTATE '40000'
				SET message_text = "Can't INSERT type to the center of hierarchy";
		END IF;
END //

#--------------------------------------------------

DELIMITER //
DROP PROCEDURE IF EXISTS before_alter_fields //
CREATE PROCEDURE before_alter_fields()
BEGIN
	DROP TABLE IF EXISTS `nz_test_closure`.`field_add_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`field_add_list`(
		`owner_type_name` VARCHAR(128) NOT NULL check(`owner_type_name` != ''),
		`name` VARCHAR(128) NOT NULL check(`name` != ''),
		`col_name` VARCHAR(64) NOT NULL COLLATE `utf8_general_ci` check(`col_name` != ''),
		`type_name` VARCHAR(64) NOT NULL check(`type_name` != ''),
		`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
		`compare_options` ENUM
			(
				'None',
				'IgnoreCase',
				'IgnoreNonSpace',
				'IgnoreSymbols',
				'IgnoreKanaType',
				'IgnoreWidth',
				'OrdinalIgnoreCase',
				'StringSort',
				'Ordinal'
			) NOT NULL DEFAULT 'None',
		`back_ref_name` VARCHAR(128) DEFAULT NULL check(`back_ref_name` != '')
	) ENGINE=`MEMORY` DEFAULT CHARSET=`utf8` COLLATE `utf8_general_ci`;

# ---> fields_rem_list
END //

DELIMITER //
DROP PROCEDURE IF EXISTS add_all_fields //
CREATE PROCEDURE add_all_fields()
BEGIN
	INSERT INTO `nz_test_closure`.`field`
	(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`, `owner_type_id`, `ref_type_id`)
	SELECT newf.`name`, newf.`col_name`, newf.`owner_type_name`, newf.`type_name`, newf.`is_list`, newf.`compare_options`, newf.`back_ref_name`, ownt.`id`, reft.`id`
	FROM `nz_test_closure`.`field_add_list` AS newf
	JOIN `nz_test_closure`.`type` AS ownt
	ON ownt.`name` = newf.`owner_type_name`
	left JOIN `nz_test_closure`.`type` AS reft
	ON reft.`name` = newf.`type_name`;

	update `nz_test_closure`.`field` AS f1
	JOIN `nz_test_closure`.`field` AS f2
	ON f2.`name` = f1.`back_ref_name`
	SET f1.`back_ref_id` = f2.`id`;

	DROP TABLE IF EXISTS `nz_test_closure`.`new_field`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`new_field`(
		`id` INT NOT NULL,
		FOREIGN KEY (`id`)
			REFERENCES `nz_test_closure`.`field`(`id`)
			ON DELETE CASCADE
	) ENGINE=`MEMORY`;
	
	INSERT INTO `nz_test_closure`.`new_field`
	SELECT f.`id`
	FROM `nz_test_closure`.`field` AS f
	JOIN `nz_test_closure`.`field_add_list` AS newf
	ON f.`name` = newf.`name`
		AND f.`owner_type_name` = newf.`owner_type_name`;

# ---> autofill BackReferences

	# Update all types with new fields
	BEGIN
		DECLARE db_name VARCHAR(64) DEFAULT 'nz_test_closure';

		DECLARE t_type_name VARCHAR(128) DEFAULT NULL;
		DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
		DECLARE fields_defs TEXT;
		DECLARE fields_constraints TEXT;

		DECLARE types_done boolean DEFAULT FALSE;
		DECLARE type_cur CURSOR FOR
			SELECT t.`name` ,t.`table_name`
			FROM `nz_test_closure`.`type` AS t;
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET types_done = true;
		
		OPEN type_cur;

		FETCH type_cur	
		INTO t_type_name, t_table_name;
		WHILE NOT types_done DO
			CALL get_type_fields_and_constraints(t_type_name, FALSE, fields_defs, fields_constraints);

			IF (LENGTH(fields_defs) > 0) THEN
				IF (LENGTH(fields_constraints) > 0) THEN 
					SET fields_constraints = CONCAT(',', fields_constraints);
				END IF;

				SET @prep_str = CONCAT('
					ALTER TABLE `', db_name ,'`.`', t_table_name, '`
						ADD COLUMN (', fields_defs ,')
						', fields_constraints, ';
					');
/*
# Debug
				SELECT @prep_str AS 'Altering query';
*/
				PREPARE p_alter_table FROM @prep_str;
				EXECUTE p_alter_table;
				DEALLOCATE PREPARE p_alter_table;
			END IF;

			FETCH type_cur
			INTO t_type_name, t_table_name;
		END WHILE;

		CLOSE type_cur;
	END;

	DROP TABLE `nz_test_closure`.`new_field`;
	TRUNCATE TABLE `nz_test_closure`.`field_add_list`;
END //

#--------------------------------------------------
/*
DELIMITER //
DROP PROCEDURE IF EXISTS get_all_ancestors//
CREATE PROCEDURE get_all_ancestors(id INT)
BEGIN
	SELECT t.name
	FROM `nz_test_closure`.`type` AS t
	JOIN `nz_test_closure`.`type_closure` AS clos
	ON t.id = clos.ancestor
	WHERE clos.descendant = id
		AND t.id != id;	# filter itself
END //

DELIMITER //
DROP PROCEDURE IF EXISTS get_all_descendants//
CREATE PROCEDURE get_all_descendants(id INT)
BEGIN
	SELECT t.name
	FROM `nz_test_closure`.`type` AS t
	JOIN `nz_test_closure`.`type_closure` AS clos
	ON t.id = clos.descendant
	WHERE clos.ancestor = id
		AND t.id != id;	# filter itself
END //

DELIMITER ;
*/
