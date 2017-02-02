/*======================================

		Nezaboodka Admin database
			type procedures

======================================*/

USE `nz_test_closure`;


DELIMITER //
DROP PROCEDURE IF EXISTS before_alter_types //
CREATE PROCEDURE before_alter_types()
BEGIN
	DROP TABLE IF EXISTS `nz_test_closure`.`type_add_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`type_add_list`(
		`name` VARCHAR(128) NOT NULL UNIQUE CHECK(`name` != ''),
		`table_name` VARCHAR(64) NOT NULL UNIQUE CHECK(`table_name` != ''),
		`base_type_name` VARCHAR(128) CHECK(`table_name` != '')
	) ENGINE=`MEMORY` DEFAULT CHARSET=`UTF8` COLLATE `UTF8_GENERAL_CI`;

-- ----> type_rem_list
END //

/*---------------------------------------/
			Add types
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS add_types //
CREATE PROCEDURE add_types()
BEGIN
	DROP TABLE IF EXISTS `nz_test_closure`.`type_add_queue`;
	CREATE TEMPORARY TABLE `nz_test_closure`.`type_add_queue`(
		`ord` INT NOT NULL,
		`id` INT NOT NULL UNIQUE,	-- to IGNORE already inserted elements
		FOREIGN KEY (`id`)
			REFERENCES `nz_test_closure`.`type`(`id`)
			ON DELETE CASCADE
	) ENGINE=`MEMORY`;

	DROP TABLE IF EXISTS `nz_test_closure`.`new_type`;
	CREATE TEMPORARY TABLE `nz_test_closure`.`new_type`(
		`id` INT NOT NULL,
		FOREIGN KEY (`id`)
			REFERENCES `nz_test_closure`.`type`(`id`)
			ON DELETE CASCADE
	) ENGINE=`MEMORY`;

	CALL _order_insert_new_types();
	TRUNCATE TABLE `nz_test_closure`.`type_add_list`;

	CALL _add_new_types_to_closure();
	DROP TABLE `nz_test_closure`.`type_add_queue`;

	CALL _process_new_types();
	DROP TABLE `nz_test_closure`.`new_type`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _order_insert_new_types //
CREATE PROCEDURE _order_insert_new_types()
BEGIN
	DECLARE last_insert_count INT DEFAULT 0;
	DECLARE current_ord INT DEFAULT 0;

	SET current_ord = 0;
	CALL _ord_insert_roots(current_ord);

	ORDER_LOOP: LOOP
		SET current_ord = current_ord + 1;
		CALL _ord_insert_existing_children(current_ord, last_insert_count);

		IF last_insert_count = 0 THEN
			LEAVE ORDER_LOOP;
		END IF;
	END LOOP;
/*
-- Debug
	SELECT q.`ord`, tadd.`name`, tadd.`base_type_name`
	FROM `nz_test_closure`.`type` AS tadd
	JOIN `nz_test_closure`.`type_add_queue` AS q
	ON tadd.`id` = q.`id`
	ORDER BY q.`ord`;
*/
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _ord_insert_roots //
CREATE PROCEDURE _ord_insert_roots(IN current_ord INT)
BEGIN
	DECLARE type_id INT;

	DECLARE t_name VARCHAR(128);
	DECLARE t_base_name VARCHAR(128);
	DECLARE t_table_name VARCHAR(64);

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE cur CURSOR FOR
		SELECT `name`, `base_type_name`, `table_name`
		FROM `nz_test_closure`.`type_add_list`
		WHERE `base_type_name` IS NULL;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	OPEN cur;
	FETCH cur
	INTO t_name, t_base_name, t_table_name;
	WHILE NOT done DO	
		INSERT INTO `nz_test_closure`.`type`
		(`name`, `base_type_name`, `table_name`)
		VALUE
		(t_name, t_base_name, t_table_name);

		SELECT LAST_INSERT_ID()
		INTO type_id;

		INSERT INTO `nz_test_closure`.`type_add_queue`
		(`ord`, `id`)
		VALUE
		(current_ord, type_id);

		INSERT INTO `nz_test_closure`.`new_type`
		(`id`)
		VALUE 
		(type_id);

        FETCH cur
		INTO t_name, t_base_name, t_table_name;
	END WHILE;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _ord_insert_existing_children //
CREATE PROCEDURE _ord_insert_existing_children(IN current_ord INT, OUT insert_count INT)
BEGIN
	DECLARE type_id INT;
	DECLARE last_insert_count INT DEFAULT 0;

	DECLARE t_name VARCHAR(128);
	DECLARE t_base_name VARCHAR(128);
	DECLARE t_table_name VARCHAR(64);

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE cur CURSOR FOR
		SELECT tadd.`name`, tadd.`base_type_name`, tadd.`table_name`
		FROM `nz_test_closure`.`type_add_list` AS tadd
		JOIN `nz_test_closure`.`type` AS t
		ON tadd.`base_type_name` = t.`name`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	SET insert_count = 0;

	OPEN cur;
	FETCH cur
	INTO t_name, t_base_name, t_table_name;
	WHILE NOT done DO
		SET @@session.last_insert_id=0;

		INSERT IGNORE INTO `nz_test_closure`.`type`
		(`name`, `base_type_name`, `table_name`)
		VALUE
		(t_name, t_base_name, t_table_name);

		SELECT LAST_INSERT_ID()
		INTO type_id;
		IF (type_id != 0) THEN
			SET insert_count = insert_count + 1;

			INSERT INTO `nz_test_closure`.`type_add_queue`
			(`ord`, `id`)
			VALUE
			(current_ord, type_id);

			INSERT INTO `nz_test_closure`.`new_type`
			(`id`)
			VALUE 
			(type_id);
		END IF;

        FETCH cur
		INTO t_name, t_base_name, t_table_name;
	END WHILE;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _add_new_types_to_closure //
CREATE PROCEDURE _add_new_types_to_closure()
BEGIN
	DECLARE base_id INT DEFAULT NULL;
	DECLARE type_id INT DEFAULT NULL;
	
	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE cur CURSOR FOR
		SELECT tadd.`id`, tbase.`id`
		FROM `nz_test_closure`.`type` AS tadd
		JOIN `nz_test_closure`.`type_add_queue` AS q
		ON tadd.`id` = q.`id`
		LEFT JOIN `nz_test_closure`.`type` AS tbase
		ON tbase.`name` = tadd.`base_type_name`
		ORDER BY q.`ord`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;
	OPEN cur;

	FETCH cur
	INTO type_id, base_id;
	WHILE NOT done DO		
		INSERT INTO `nz_test_closure`.`type_closure`
		(`ancestor`, `descendant`)
		SELECT clos.`ancestor`, type_id
		FROM `nz_test_closure`.`type_closure` AS clos
		WHERE clos.`descendant` = base_id
		UNION
		SELECT type_id, type_id;
		
		FETCH cur
		INTO type_id, base_id;
	END WHILE;
	
	CLOSE cur;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _process_new_types //
CREATE PROCEDURE _process_new_types()
BEGIN
	DECLARE db_name VARCHAR(64) DEFAULT 'nz_test_closure';

	DECLARE t_type_id INT DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE fields_defs TEXT;
	DECLARE fields_constraints TEXT;

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE new_type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `nz_test_closure`.`type` AS t
		JOIN `nz_test_closure`.`new_type` AS n
		WHERE t.`id` = n.`id`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;
	
	OPEN new_type_cur;

	FETCH new_type_cur	
	INTO t_type_id, t_table_name;
	WHILE NOT types_done DO

		CALL _get_type_new_fields_and_constraints(t_type_id, TRUE, fields_defs, fields_constraints);

		IF (CHAR_LENGTH(fields_defs) > 0) THEN 
			SET fields_defs = CONCAT(',', fields_defs);
		END IF;

		IF (CHAR_LENGTH(fields_constraints) > 0) THEN 
			SET fields_constraints = CONCAT(',', fields_constraints);
		END IF;

		-- Create table for type with all ancestors' fields
		--  (table name can't be a parameter => prepare each time)
		SET @prep_str = CONCAT('
			CREATE TABLE `', db_name ,'`.`', t_table_name, '` (
				id BIGINT(0) PRIMARY KEY NOT NULL

				', fields_defs ,',

				FOREIGN KEY (id)
					REFERENCES `', db_name ,'`.`db_key`(`sys_id`)
					ON DELETE CASCADE	-- delete object when it\'s key is removed

					', fields_constraints, '

			) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;
		');
/*
-- Debug
		SELECT @prep_str;
*/
		PREPARE p_create_table FROM @prep_str;
		EXECUTE p_create_table;
		DEALLOCATE PREPARE p_create_table;

		FETCH new_type_cur
		INTO t_type_id, t_table_name;
	END WHILE;
	
	CLOSE new_type_cur;
END //
