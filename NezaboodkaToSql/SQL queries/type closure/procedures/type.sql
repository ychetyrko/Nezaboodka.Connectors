/*======================================

		Nezaboodka Admin database
			type procedures

======================================*/

USE `nz_test_closure`;


DELIMITER //
DROP PROCEDURE IF EXISTS _before_alter_types //
CREATE PROCEDURE _before_alter_types()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_add_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`type_add_list`(
		`name` VARCHAR(128) NOT NULL UNIQUE
			CHECK(`name` != ''),
		`table_name` VARCHAR(64) NOT NULL UNIQUE COLLATE `UTF8_GENERAL_CI`
			CHECK(`table_name` != ''),
		`base_type_name` VARCHAR(128)
			CHECK(`table_name` != '')
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_rem_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`type_rem_list`(
		`name` VARCHAR(128) NOT NULL UNIQUE
			CHECK(`name` != '')
	) ENGINE=`MEMORY`;
END //

/*---------------------------------------/
			Add types
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS _temp_before_add_types //
CREATE PROCEDURE _temp_before_add_types()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_add_queue`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`type_add_queue`(
		`ord` INT NOT NULL,
		`id` INT NOT NULL
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`new_type`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`new_type`(
		`id` INT NOT NULL
	) ENGINE=`MEMORY`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_after_add_types //
CREATE PROCEDURE _temp_after_add_types()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`new_type`;
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_add_queue`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _add_types //
CREATE PROCEDURE _add_types()
BEGIN
	DECLARE inserted_types_count INT DEFAULT 0;
	DECLARE add_types_count INT DEFAULT 0;

	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
-- RESIGNAL;
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some types can't be added";
	END;

	CALL _order_insert_new_types();

	SELECT COUNT(`id`)
	INTO inserted_types_count
	FROM `nz_admin_db`.`type_add_queue`;
    
    SELECT COUNT(`name`)
	INTO add_types_count
	FROM `nz_admin_db`.`type_add_list`;

	IF (inserted_types_count != add_types_count) THEN
		SIGNAL SQLSTATE '40011';
	END IF;

	DELETE FROM `nz_admin_db`.`type_add_list`;

	CALL _add_new_types_to_closure();
	CALL _process_new_types();
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
CREATE PROCEDURE _ord_insert_roots(
	IN current_ord INT
)
BEGIN
	DECLARE type_id INT;

	DECLARE t_name VARCHAR(128);
	DECLARE t_base_name VARCHAR(128);
	DECLARE t_table_name VARCHAR(64);

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE cur CURSOR FOR
		SELECT `name`, `base_type_name`, `table_name`
		FROM `nz_admin_db`.`type_add_list`
		WHERE `base_type_name` IS NULL;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	OPEN cur;
	FETCH cur
	INTO t_name, t_base_name, t_table_name;
	WHILE NOT done DO	
		INSERT INTO `{@db_name}`.`type`
		(`name`, `base_type_name`, `table_name`)
		VALUE
		(t_name, t_base_name, t_table_name);

		SELECT LAST_INSERT_ID()
		INTO type_id;

		INSERT INTO `nz_admin_db`.`type_add_queue`
		(`ord`, `id`)
		VALUE
		(current_ord, type_id);

		INSERT INTO `nz_admin_db`.`new_type`
		(`id`)
		VALUE 
		(type_id);

        FETCH cur
		INTO t_name, t_base_name, t_table_name;
	END WHILE;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _ord_insert_existing_children //
CREATE PROCEDURE _ord_insert_existing_children(
	IN current_ord INT,
	OUT insert_count INT
)
BEGIN
	DECLARE type_id INT;
	DECLARE last_insert_count INT DEFAULT 0;

	DECLARE t_name VARCHAR(128);
	DECLARE t_base_name VARCHAR(128);
	DECLARE t_table_name VARCHAR(64);

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE cur CURSOR FOR
		SELECT tadd.`name`, tadd.`base_type_name`, tadd.`table_name`
		FROM `nz_admin_db`.`type_add_list` AS tadd
		JOIN `{@db_name}`.`type` AS t
		ON tadd.`base_type_name` = t.`name`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	SET insert_count = 0;

	OPEN cur;
	FETCH cur
	INTO t_name, t_base_name, t_table_name;
	WHILE NOT done DO
		SET @@SESSION.last_insert_id = 0;

		INSERT IGNORE INTO `{@db_name}`.`type`
		(`name`, `base_type_name`, `table_name`)
		VALUE
		(t_name, t_base_name, t_table_name);

		SELECT LAST_INSERT_ID()
		INTO type_id;
		IF (type_id != 0) THEN
			SET insert_count = insert_count + 1;

			INSERT INTO `nz_admin_db`.`type_add_queue`
			(`ord`, `id`)
			VALUE
			(current_ord, type_id);

			INSERT INTO `nz_admin_db`.`new_type`
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
		FROM `{@db_name}`.`type` AS tadd
		JOIN `nz_admin_db`.`type_add_queue` AS q
		ON tadd.`id` = q.`id`
		LEFT JOIN `{@db_name}`.`type` AS tbase
		ON tbase.`name` = tadd.`base_type_name`
		ORDER BY q.`ord`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;
	OPEN cur;

	FETCH cur
	INTO type_id, base_id;
	WHILE NOT done DO		
		INSERT INTO `{@db_name}`.`type_closure`
		(`ancestor`, `descendant`)
		SELECT clos.`ancestor`, type_id
		FROM `{@db_name}`.`type_closure` AS clos
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
	DECLARE t_type_id INT DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE fields_defs TEXT;
	DECLARE fields_constraints TEXT;

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE new_type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `{@db_name}`.`type` AS t
		JOIN `nz_admin_db`.`new_type` AS n
		WHERE t.`id` = n.`id`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;
	
	OPEN new_type_cur;

	FETCH new_type_cur	
	INTO t_type_id, t_table_name;
	WHILE NOT types_done DO

		CALL _get_type_new_fields_and_constraints(
			t_type_id, TRUE, fields_defs, fields_constraints
		);

		IF (CHAR_LENGTH(fields_defs) > 0) THEN 
			SET fields_defs = CONCAT(',', fields_defs);
		END IF;

		IF (CHAR_LENGTH(fields_constraints) > 0) THEN 
			SET fields_constraints = CONCAT(',', fields_constraints);
		END IF;

		SET @prep_str = CONCAT('
			CREATE TABLE `', {@db_name} ,'`.`', t_table_name, '` (
				id BIGINT(0) PRIMARY KEY NOT NULL

				', fields_defs ,',

				FOREIGN KEY (id)
					REFERENCES `db_key`(`sys_id`)
					ON DELETE CASCADE	-- delete object when it\'s key is removed

					', fields_constraints, '

			) ENGINE=`InnoDB`;
		');
/*
-- Debug
		SELECT @prep_str;
*/
		-- check if types are valid
		PREPARE p_create_table FROM @prep_str;
		DEALLOCATE PREPARE p_create_table;

		INSERT INTO `nz_admin_db`.`alter_query`
		(`query_text`)
		VALUE
		(@prep_str);

		FETCH new_type_cur
		INTO t_type_id, t_table_name;
	END WHILE;
	
	CLOSE new_type_cur;
END //

/*---------------------------------------/
			Remove types
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS _temp_before_remove_types //
CREATE PROCEDURE _temp_before_remove_types()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_types_list`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`removing_types_list`(
		`id` INT NOT NULL UNIQUE,
		`constr` TEXT NOT NULL,
		`table_name` VARCHAR(64) NOT NULL
	) ENGINE=`INNODB`;	-- TEXT is not supported in MEMORY engine

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_fields_list`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`removing_fields_list`(
		`id` INT NOT NULL UNIQUE
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_types_buf`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`removing_types_buf`(
		`id` INT NOT NULL UNIQUE,
		`name` VARCHAR(128) NOT NULL UNIQUE CHECK(`name` != '')
	) ENGINE=`MEMORY`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_after_remove_types //
CREATE PROCEDURE _temp_after_remove_types()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_types_buf`;
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_fields_list`;
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_types_list`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_types //
CREATE PROCEDURE _remove_types()
BEGIN
	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
-- RESIGNAL;
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some types can't be removed";
	END;

	CALL _get_removing_types_constr();
	CALL _remove_types_fields_from_table();
	CALL _remove_types_from_closure();
/*
-- Debug
	SELECT * FROM `nz_test_closure`.`removing_types_list`;
*/
	DELETE FROM `{@db_name}`.`type`
	WHERE `id` IN (
		SELECT `id`
		FROM `nz_admin_db`.`removing_types_list`
	);

	CALL _process_removed_types();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _get_removing_types_constr //
CREATE PROCEDURE _get_removing_types_constr()
BEGIN
	DECLARE t_type_id INT DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE dropping_constraints TEXT DEFAULT '';

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `{@db_name}`.`type` AS t
		JOIN `nz_admin_db`.`type_rem_list` AS remt
		WHERE t.`name` = remt.`name`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;
	
	OPEN type_cur;

	FETCH type_cur	
	INTO t_type_id, t_table_name;
	WHILE NOT types_done DO
		CALL _get_type_constraints(t_type_id, dropping_constraints);
		
		IF (CHAR_LENGTH(dropping_constraints) > 0) THEN 
			SET dropping_constraints = CONCAT(
				'ALTER TABLE `', {@db_name}, '`.`', t_table_name, '`
				', dropping_constraints, ';'
			);
		END IF;

		INSERT INTO `nz_admin_db`.`removing_types_list`
		(`id`, `table_name`, `constr`)
		VALUE
		(t_type_id, t_table_name, dropping_constraints);

		FETCH type_cur	
		INTO t_type_id, t_table_name;
	END WHILE;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_types_fields_from_table //
CREATE PROCEDURE _remove_types_fields_from_table()
BEGIN
	INSERT INTO `nz_admin_db`.`removing_fields_list`
	(`id`)
	SELECT `id`
	FROM `{@db_name}`.`field`
	WHERE `owner_type_id` IN (
		SELECT `id`
		FROM `nz_admin_db`.`removing_types_list`
	);

	CALL _remove_deleted_fields_from_table();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _get_type_constraints //
CREATE PROCEDURE _get_type_constraints(
	IN c_type_id INT,
	OUT drop_constraints TEXT
)
BEGIN
	DECLARE cf_col_name VARCHAR(64) DEFAULT NULL;
	DECLARE cf_ref_type_id INT DEFAULT NULL;
	DECLARE cf_id INT DEFAULT NULL;

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE fields_cur CURSOR FOR
		SELECT `id`, `ref_type_id`
		FROM `{@db_name}`.`field` 
		WHERE `owner_type_id` IN (
			SELECT clos.`ancestor`	-- get all super classes
			FROM `{@db_name}`.`type_closure` AS clos
			WHERE clos.`descendant` = c_type_id
		);
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	SET drop_constraints = '';

	OPEN fields_cur;

	FETCH fields_cur
	INTO cf_id, cf_ref_type_id;
	WHILE NOT done DO
		IF !(cf_ref_type_id IS NULL) THEN
			SET drop_constraints = CONCAT(drop_constraints, ',
			DROP FOREIGN KEY `FK_', c_type_id, '_', cf_id, '`');
		END IF;
		
		FETCH fields_cur
		INTO cf_id, cf_ref_type_id;
	END WHILE;
	
	IF (LEFT(drop_constraints, 1) = ',') THEN
		SET drop_constraints = SUBSTRING(drop_constraints, 2);
	END IF;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_types_from_closure //
CREATE PROCEDURE _remove_types_from_closure()
BEGIN
	DECLARE rest_count INT DEFAULT 0;

	LP_TERM: LOOP
/*
-- Debug
		SELECT t.`id`, t.`name`
		FROM `{@db_name}`.`type` AS t
		JOIN `nz_admin_db`.`removing_types_list` AS remtlist
		ON t.`id` = remtlist.`id`
		WHERE (
			SELECT COUNT(clos.`ancestor`)
			FROM `{@db_name}`.`type_closure` AS clos
			WHERE clos.`ancestor` = t.`id`
		) = 1;	-- Сheck if terminating type
*/
		INSERT INTO `nz_admin_db`.`removing_types_buf`
		(`id`, `name`)
		SELECT t.`id`, t.`name`
		FROM `{@db_name}`.`type` AS t
		JOIN `nz_admin_db`.`removing_types_list` AS remtlist
		ON t.`id` = remtlist.`id`
		WHERE (
			SELECT COUNT(clos.`ancestor`)
			FROM `{@db_name}`.`type_closure` AS clos
			WHERE clos.`ancestor` = t.`id`
		) = 1;	-- Сheck if terminating type
/*
-- Debug
		SELECT *
		FROM `nz_admin_db`.`removing_types_buf`;
*/
		IF (ROW_COUNT() = 0) THEN
			LEAVE LP_TERM;
		END IF;
		
		DELETE FROM `{@db_name}`.`type_closure`
		WHERE `descendant` IN (
			SELECT `id`
			FROM `nz_admin_db`.`removing_types_buf`
		);

		DELETE FROM `nz_admin_db`.`type_rem_list`
		WHERE `name` IN (
			SELECT `name`
			FROM `nz_admin_db`.`removing_types_buf`
		);

		DELETE FROM `nz_admin_db`.`removing_types_buf`;
	END LOOP;
/*
-- Debug
	SELECT * FROM `nz_admin_db`.`type_rem_list`;
	SELECT COUNT(`name`)
	FROM `nz_admin_db`.`type_rem_list`;
	SELECT rest_count;
*/
	SELECT COUNT(`name`)
	INTO rest_count
	FROM `nz_admin_db`.`type_rem_list`;

	IF (rest_count > 0) THEN
-- TODO: write invalid typenames
		SIGNAL SQLSTATE 'HY000';
	END IF;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _process_removed_types //
CREATE PROCEDURE _process_removed_types()
BEGIN
	DECLARE t_type_id INT DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE dropping_constraints TEXT DEFAULT '';

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE rem_type_cur CURSOR FOR
		SELECT `id`, `table_name`, `constr`
		FROM `nz_admin_db`.`removing_types_list`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;

	OPEN rem_type_cur;

	FETCH rem_type_cur	
	INTO t_type_id, t_table_name, dropping_constraints;
	WHILE NOT types_done DO
		IF (dropping_constraints != '') THEN
			SET @prep_str = dropping_constraints;
/*
-- Debug
		SELECT @prep_str;
*/
			INSERT INTO `nz_admin_db`.`alter_query`
			(`query_text`)
			VALUE
			(@prep_str);
		END IF;

		SET @prep_str = CONCAT('DROP TABLE `', {@db_name} ,'`.`', t_table_name, '`;');
/*
-- Debug
		SELECT @prep_str;
*/
		INSERT INTO `nz_admin_db`.`alter_query`
		(`query_text`)
		VALUE
		(@prep_str);

		FETCH rem_type_cur
		INTO t_type_id, t_table_name, dropping_constraints;
	END WHILE;
	
	CLOSE rem_type_cur;
END //
